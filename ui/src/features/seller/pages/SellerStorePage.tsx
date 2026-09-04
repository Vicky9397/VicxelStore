import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useState, type ReactElement } from 'react';
import { useTranslation } from 'react-i18next';
import { FormError } from '@/components/forms/FormError';
import { Spinner } from '@/components/ui/Spinner';
import {
  createStore,
  getMyStore,
  submitKyc,
  updateBankInfo,
  updateTaxInfo,
} from '@/features/seller/api/sellerApi';
import { ApiRequestError } from '@/lib/apiClient';

/**
 * Store setup and the verification steps a seller must clear before publishing:
 * identity, tax registration and payout destination (spec 02 section 2.4).
 */
export function SellerStorePage(): ReactElement {
  const { t } = useTranslation();
  const queryClient = useQueryClient();

  const store = useQuery({
    queryKey: ['my-store'],
    queryFn: getMyStore,
    retry: false,
  });

  const invalidate = (): void => {
    void queryClient.invalidateQueries({ queryKey: ['my-store'] });
  };

  const hasNoStore = store.isError
    && store.error instanceof ApiRequestError
    && store.error.status === 404;

  if (store.isPending) {
    return <Spinner />;
  }

  if (hasNoStore) {
    return <CreateStoreForm onCreated={invalidate} />;
  }

  if (store.isError) {
    return <FormError error={store.error} />;
  }

  const { store: details, profile } = store.data;

  return (
    <div className="row g-4">
      <div className="col-lg-7">
        <h1 className="h3">{details.name}</h1>
        <p className="text-muted">/s/{details.slug}</p>

        <div
          className={profile.isPublishReady ? 'alert alert-success' : 'alert alert-warning'}
          role="status"
        >
          {profile.isPublishReady ? t('seller.readyToPublish') : t('seller.completeOnboarding')}
        </div>

        <ol className="list-group list-group-numbered">
          <li className="list-group-item d-flex justify-content-between align-items-start">
            <div className="me-2">
              <div className="fw-semibold">{t('seller.stepKyc')}</div>
              <div className="text-muted small">{t('seller.stepKycHelp')}</div>
            </div>
            <span className="badge text-bg-secondary">{profile.kycStatus}</span>
          </li>
          <li className="list-group-item d-flex justify-content-between align-items-start">
            <div className="me-2">
              <div className="fw-semibold">{t('seller.stepTax')}</div>
              <div className="text-muted small">{t('seller.stepTaxHelp')}</div>
            </div>
            <span className="badge text-bg-secondary">
              {profile.taxId === null ? t('seller.missing') : profile.taxIdType}
            </span>
          </li>
          <li className="list-group-item d-flex justify-content-between align-items-start">
            <div className="me-2">
              <div className="fw-semibold">{t('seller.stepBank')}</div>
              <div className="text-muted small">{t('seller.stepBankHelp')}</div>
            </div>
            <span className="badge text-bg-secondary">
              {profile.bankVerified ? t('seller.verified') : t('seller.pending')}
            </span>
          </li>
        </ol>
      </div>

      <div className="col-lg-5">
        <OnboardingForms storeId={details.id} onSaved={invalidate} />
      </div>
    </div>
  );
}

function CreateStoreForm({ onCreated }: { onCreated: () => void }): ReactElement {
  const { t } = useTranslation();
  const [slug, setSlug] = useState('');
  const [name, setName] = useState('');

  const mutation = useMutation({
    mutationFn: () => createStore({ slug, name }),
    onSuccess: onCreated,
  });

  return (
    <div className="row justify-content-center">
      <div className="col-md-6">
        <h1 className="h3">{t('seller.openStoreTitle')}</h1>
        <p className="text-muted">{t('seller.openStoreSubtitle')}</p>

        <FormError error={mutation.error} />

        <form
          onSubmit={(event) => {
            event.preventDefault();
            mutation.mutate();
          }}
        >
          <div className="mb-3">
            <label className="form-label" htmlFor="store-name">
              {t('seller.storeName')}
            </label>
            <input
              id="store-name"
              className="form-control"
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
            />
          </div>
          <div className="mb-3">
            <label className="form-label" htmlFor="store-slug">
              {t('seller.storeSlug')}
            </label>
            <input
              id="store-slug"
              className="form-control"
              value={slug}
              onChange={(event) => setSlug(event.target.value)}
              aria-describedby="store-slug-help"
              required
            />
            <div className="form-text" id="store-slug-help">
              {t('seller.storeSlugHelp')}
            </div>
          </div>
          <button className="btn btn-primary" type="submit" disabled={mutation.isPending}>
            {t('seller.openStoreSubmit')}
          </button>
        </form>
      </div>
    </div>
  );
}

function OnboardingForms({
  storeId,
  onSaved,
}: {
  storeId: string;
  onSaved: () => void;
}): ReactElement {
  const { t } = useTranslation();
  const [legalName, setLegalName] = useState('');
  const [taxIdType, setTaxIdType] = useState('GSTIN');
  const [taxId, setTaxId] = useState('');
  const [bankRef, setBankRef] = useState('');

  const kyc = useMutation({
    mutationFn: () => submitKyc(storeId, legalName),
    onSuccess: onSaved,
  });
  const tax = useMutation({
    mutationFn: () => updateTaxInfo(storeId, taxIdType, taxId),
    onSuccess: onSaved,
  });
  const bank = useMutation({
    mutationFn: () => updateBankInfo(storeId, bankRef),
    onSuccess: onSaved,
  });

  return (
    <div className="card">
      <div className="card-body">
        <h2 className="h6">{t('seller.onboardingTitle')}</h2>

        <FormError error={kyc.error ?? tax.error ?? bank.error} />

        <form
          className="mb-4"
          onSubmit={(event) => {
            event.preventDefault();
            kyc.mutate();
          }}
        >
          <label className="form-label" htmlFor="legal-name">
            {t('seller.legalName')}
          </label>
          <input
            id="legal-name"
            className="form-control mb-2"
            value={legalName}
            onChange={(event) => setLegalName(event.target.value)}
            required
          />
          <button className="btn btn-outline-primary btn-sm" type="submit" disabled={kyc.isPending}>
            {t('seller.submitKyc')}
          </button>
        </form>

        <form
          className="mb-4"
          onSubmit={(event) => {
            event.preventDefault();
            tax.mutate();
          }}
        >
          <label className="form-label" htmlFor="tax-id-type">
            {t('seller.taxIdType')}
          </label>
          <select
            id="tax-id-type"
            className="form-select mb-2"
            value={taxIdType}
            onChange={(event) => setTaxIdType(event.target.value)}
          >
            <option value="GSTIN">GSTIN</option>
            <option value="VAT">VAT</option>
          </select>
          <label className="form-label" htmlFor="tax-id">
            {t('seller.taxId')}
          </label>
          <input
            id="tax-id"
            className="form-control mb-2"
            value={taxId}
            onChange={(event) => setTaxId(event.target.value)}
            required
          />
          <button className="btn btn-outline-primary btn-sm" type="submit" disabled={tax.isPending}>
            {t('seller.saveTaxInfo')}
          </button>
        </form>

        <form
          onSubmit={(event) => {
            event.preventDefault();
            bank.mutate();
          }}
        >
          <label className="form-label" htmlFor="bank-ref">
            {t('seller.bankRef')}
          </label>
          <input
            id="bank-ref"
            className="form-control mb-2"
            value={bankRef}
            onChange={(event) => setBankRef(event.target.value)}
            aria-describedby="bank-ref-help"
            required
          />
          <div className="form-text mb-2" id="bank-ref-help">
            {t('seller.bankRefHelp')}
          </div>
          <button className="btn btn-outline-primary btn-sm" type="submit" disabled={bank.isPending}>
            {t('seller.saveBankInfo')}
          </button>
        </form>
      </div>
    </div>
  );
}
